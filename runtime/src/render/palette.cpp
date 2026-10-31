#include "agb/render/palette.hpp"

#include "agb/commit.hpp"
#include "agb/debug.hpp"

namespace agb::render::palettes {

namespace {
AGB_EWRAM_BSS u16 g_base[512];
u32 g_changed[16];

struct Owner {
    const data::Palette* palette;
    u8 refs;
};
AGB_EWRAM_BSS Owner g_owner[32];  // per bank: 0-15 BG, 16-31 OBJ (used when palettes are taken or given back)

struct Fade {
    u32 mask;
    u16 color;
    u16 frames, elapsed;
    u8 from, to, current;
};
Fade g_fade{};

struct Tint {
    u32 mask;
    u16 color;
    u8 amount;
};
Tint g_tint{};

struct Cycle {
    bool active;
    bool obj;
    u8 bank, first, last, ticks, timer;
    Direction dir;
    s8 step;
    u8 offset;
};
constexpr int kCycles = 4;
Cycle g_cycles[kCycles];

struct Flash {
    u32 mask;
    u16 color;
    u8 frames;
};
Flash g_flash{};
bool g_dirty = true;     // every bank must be recomputed (reset, fades, tints, flashes)
u32 g_dirtyBanks = 0;    // only these banks changed (bit per bank: 0-15 BG, 16-31 OBJ)

u16 cycled(int index) {
    const int bankStart = index & ~15;
    const int i = index & 15;
    for (const Cycle& c : g_cycles) {
        if (!c.active || (c.obj ? 256 : 0) + c.bank * 16 != bankStart || i < c.first || i > c.last) continue;
        const int len = c.last - c.first + 1;
        int src = i - c.first + c.offset;
        src %= len;
        return g_base[bankStart + c.first + src];
    }
    return g_base[index];
}
}  // namespace

u16 blend(u16 a, u16 b, u8 amount16) {
    if (amount16 >= 16) return b;
    const int ar = a & 31, ag = (a >> 5) & 31, ab = (a >> 10) & 31;
    const int br = b & 31, bg = (b >> 5) & 31, bb = (b >> 10) & 31;
    const int r = ar + (br - ar) * amount16 / 16, g = ag + (bg - ag) * amount16 / 16, bl = ab + (bb - ab) * amount16 / 16;
    return static_cast<u16>(r | (g << 5) | (bl << 10));
}

void reset() {
    for (auto& c : g_base) c = 0;
    for (auto& o : g_owner) o = {};
    for (auto& c : g_cycles) c = {};
    g_fade = {};
    g_tint = {};
    g_flash = {};
    g_dirty = true;
}

int acquire(const data::Palette* palette, bool obj) {
    const int base = obj ? 16 : 0;
    for (int i = 0; i < 16; ++i)
        if (g_owner[base + i].palette == palette && g_owner[base + i].refs) {
            for (int b = 0; b < palette->banks; ++b) ++g_owner[base + i + b].refs;
            return i;
        }
    for (int i = 0; i + palette->banks <= 16; ++i) {
        bool free = true;
        for (int b = 0; b < palette->banks; ++b) free &= g_owner[base + i + b].refs == 0;
        if (!free) continue;
        for (int b = 0; b < palette->banks; ++b) {
            g_owner[base + i + b] = {palette, 1};
            setBank(obj, i + b, palette->colors + b * 16);
        }
        return i;
    }
    debug::logf("warning: all 16 %s palette banks are in use", obj ? "OBJ" : "BG");
    return -1;
}

void release(const data::Palette* palette, bool obj) {
    const int base = obj ? 16 : 0;
    for (int i = 0; i < 16; ++i)
        if (g_owner[base + i].palette == palette && g_owner[base + i].refs) {
            if (--g_owner[base + i].refs == 0) g_owner[base + i].palette = nullptr;
        }
}

void setBank(bool obj, int bank, const u16* colors16) {
    u16* dst = g_base + (obj ? 256 : 0) + bank * 16;
    // Index 0 is transparent in every bank; BG bank 0 index 0 is the backdrop, set by setBackdrop().
    for (int i = (!obj && bank == 0) ? 1 : 0; i < 16; ++i) dst[i] = colors16[i];
    g_dirtyBanks |= 1u << ((obj ? 16 : 0) + bank);
}

u16 baseColor(int index) { return g_base[index & 511]; }
void setBaseColor(int index, u16 c) {
    g_base[index & 511] = c;
    g_dirtyBanks |= 1u << ((index & 511) >> 4);
}
void setBackdrop(u16 c) { setBaseColor(0, c); }

int banksUsed(bool obj) {
    int n = 0;
    for (int i = 0; i < 16; ++i) n += g_owner[(obj ? 16 : 0) + i].refs ? 1 : 0;
    return n;
}

void startFade(u32 mask, u16 color, u8 amount, u16 frames) {
    g_fade.mask = mask;
    g_fade.color = color;
    g_fade.from = g_fade.current;
    g_fade.to = amount > 16 ? 16 : amount;
    g_fade.frames = frames ? frames : 1;
    g_fade.elapsed = 0;
    g_dirty = true;
}

bool fading() { return g_fade.elapsed < g_fade.frames && g_fade.from != g_fade.to; }

void setFade(u32 mask, u16 color, u8 amount) {
    g_fade = {mask, color, 1, 1, amount, amount, amount};
    g_dirty = true;
}

void setTint(u32 mask, u16 color, u8 amount) {
    g_tint = {mask, color, amount};
    g_dirty = true;
}

int startCycle(bool obj, int bank, u8 first, u8 last, u8 ticks, Direction direction) {
    for (int i = 0; i < kCycles; ++i) {
        if (g_cycles[i].active) continue;
        g_cycles[i] = {true, obj, static_cast<u8>(bank), first, last, static_cast<u8>(ticks ? ticks : 1), 0, direction, 1, 0};
        return i;
    }
    debug::log("warning: all 4 palette cycle slots are in use");
    return -1;
}

void stopCycle(int slot) {
    if (slot >= 0 && slot < kCycles) {
        g_cycles[slot].active = false;
        g_dirty = true;
    }
}

void flash(u32 mask, u16 color, u8 frames) {
    g_flash = {mask, color, frames};
    g_dirty = true;
}

void update() {
    for (auto& w : g_changed) w = 0;
    if (g_fade.elapsed < g_fade.frames) {
        ++g_fade.elapsed;
        g_fade.current = static_cast<u8>(g_fade.from + (g_fade.to - g_fade.from) * g_fade.elapsed / g_fade.frames);
        g_dirty = true;
    }
    for (Cycle& c : g_cycles) {
        if (!c.active || ++c.timer < c.ticks) continue;
        c.timer = 0;
        const int len = c.last - c.first + 1;
        if (c.dir == Direction::Left) c.offset = static_cast<u8>((c.offset + 1) % len);
        else if (c.dir == Direction::Right) c.offset = static_cast<u8>((c.offset + len - 1) % len);
        else {
            if (c.offset + c.step >= len || c.offset + c.step < 0) c.step = static_cast<s8>(-c.step);
            c.offset = static_cast<u8>(c.offset + c.step);
        }
        g_dirtyBanks |= 1u << ((c.obj ? 16 : 0) + c.bank);
    }
    if (g_flash.frames) {
        --g_flash.frames;
        g_dirty = true;
    }
    if (!g_dirty && !g_dirtyBanks) return;
    // Recompute only what changed: a menu loading one bank must not cost all 512 colours.
    const u32 banks = g_dirty ? 0xFFFFFFFFu : g_dirtyBanks;
    g_dirty = g_flash.frames != 0;
    g_dirtyBanks = 0;
    u16* out = commit::palette();
    for (int i = 0; i < 512; ++i) {
        const u32 bankBit = 1u << ((i >> 4) & 31);
        if (!(banks & bankBit)) {
            i |= 15;  // skip the rest of this bank
            continue;
        }
        u16 c = cycled(i);
        if (g_tint.amount && (g_tint.mask & bankBit)) c = blend(c, g_tint.color, g_tint.amount);
        if (g_fade.current && (g_fade.mask & bankBit)) c = blend(c, g_fade.color, g_fade.current);
        if (g_flash.frames && (g_flash.mask & bankBit) && (i & 15)) c = g_flash.color;
        if (out[i] != c) {
            out[i] = c;
            g_changed[i >> 5] |= 1u << (i & 31);
        }
    }
}

const u32* changedThisFrame() { return g_changed; }

}  // namespace agb::render::palettes
