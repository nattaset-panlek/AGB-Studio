#include "agb/commit.hpp"

#include <tonc.h>

#include "agb/debug.hpp"

namespace agb::commit {

namespace {
// One queued write of `rows` rows, `dstStride` bytes apart in VRAM: a copy reads each row from the caller's memory, a
// fill writes `value` to every halfword, a sequence writes value, value + 1 ... across the rows.
enum class Kind : u8 { Copy, Fill, Sequence };
struct Write {
    u8* dst;
    const u8* src;
    u32 rowBytes;
    u16 rows;
    u16 dstStride;
    u16 value;
    Kind kind;
};

AGB_EWRAM_BSS ObjAttr g_oam[128] AGB_ALIGN4;
AGB_EWRAM_BSS u16 g_pal[512] AGB_ALIGN4;
u16 g_regs[RegCount];
u32 g_regDirty = 0;
s32 g_bgRef[2][2];
u8 g_bgRefDirty = 0;
bool g_oamDirty = false, g_palDirty = false;
// Read once per entry in VBlank: EWRAM is fast enough, and IWRAM is kept for the stack and hot data (G-HW.04).
AGB_EWRAM_BSS Write g_queue[kDmaQueueSize];
int g_queueLen = 0;
u32 g_queueBytes = 0, g_budget = kDefaultDmaBudget, g_deferred = 0;
bool g_staged = false;
u32 g_direct = 0;

// I/O offsets of the shadowed 16-bit display registers, in Reg order.
constexpr u16 kRegOffset[RegCount] = {
    0x00, 0x08, 0x0A, 0x0C, 0x0E,
    0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E,
    0x20, 0x22, 0x24, 0x26, 0x30, 0x32, 0x34, 0x36,
    0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C, 0x50, 0x52, 0x54,
};

void dmaCopy(void* dst, const void* src, u32 bytes) {
    asm volatile("" ::: "memory");  // DMA is invisible to the compiler: finish all stores first
    if (((reinterpret_cast<u32>(dst) | reinterpret_cast<u32>(src) | bytes) & 3) == 0)
        dma3_cpy(dst, src, bytes);
    else
        for (u32 i = 0; i < bytes / 2; ++i) static_cast<u16*>(dst)[i] = static_cast<const u16*>(src)[i];
}

void dmaFill(void* dst, u16 value, u32 bytes) {
    if (((reinterpret_cast<u32>(dst) | bytes) & 3) == 0)
        dma3_fill(dst, value | (static_cast<u32>(value) << 16), bytes);
    else
        for (u32 i = 0; i < bytes / 2; ++i) static_cast<u16*>(dst)[i] = value;
}

void writeRow(Write& w) {
    switch (w.kind) {
        case Kind::Copy:
            dmaCopy(w.dst, w.src, w.rowBytes);
            w.src += w.rowBytes;
            break;
        case Kind::Fill:
            dmaFill(w.dst, w.value, w.rowBytes);
            break;
        case Kind::Sequence: {
            u16* d = reinterpret_cast<u16*>(w.dst);
            for (u32 i = 0; i < w.rowBytes / 2; ++i) d[i] = w.value++;
            break;
        }
    }
    w.dst += w.dstStride;
    --w.rows;
}

void deferred() {
    ++g_deferred;
    debug::warnOnce(1, "too much graphics data changed this frame, so the rest is copied next frame. Change fewer tiles or palettes per frame.");
}

bool push(const Write& w) {
    if (g_queueLen >= kDmaQueueSize) {
        debug::warnOnce(5, "more than 64 graphics changes were queued for one frame, so one was dropped. Change fewer text panels or pictures per frame.");
        return false;
    }
    g_queue[g_queueLen++] = w;
    g_queueBytes += w.rowBytes * w.rows;
    return true;
}

// Debug builds: staged loads and VBlank may write VRAM at once; anything else would show half-written graphics.
void checkDirect() {
#if defined(AGB_DEBUG)
    if (g_staged || REG_VCOUNT >= 160 || (REG_DISPCNT & DCNT_BLANK)) return;
    ++g_direct;
    debug::warnOnce(4, "graphics memory was written at once while the picture was showing, so the screen may tear. Queue the change for VBlank (commit queue) or make it in a scene load.");
#endif
}

void writeRegisters(u32 mask) {
    for (int i = 0; i < RegCount; ++i)
        if (mask & (1u << i)) *reinterpret_cast<volatile u16*>(REG_BASE + kRegOffset[i]) = g_regs[i];
}
}  // namespace

void init() {
    for (auto& o : g_oam) o = {ATTR0_HIDE, 0, 0, 0};
    for (auto& c : g_pal) c = 0;
    for (auto& r : g_regs) r = 0;
    g_regs[Bg2Pa] = g_regs[Bg2Pd] = g_regs[Bg3Pa] = g_regs[Bg3Pd] = 0x100;
    g_regDirty = (1u << RegCount) - 1;
    g_oamDirty = g_palDirty = true;
    g_queueLen = 0;
    g_queueBytes = 0;
}

void flush() {
    asm volatile("" ::: "memory");
    if (g_regDirty) {
        writeRegisters(g_regDirty);
        g_regDirty = 0;
    }
    if (g_bgRefDirty & 1) {
        REG_BG2X = g_bgRef[0][0];
        REG_BG2Y = g_bgRef[0][1];
    }
    if (g_bgRefDirty & 2) {
        REG_BG3X = g_bgRef[1][0];
        REG_BG3Y = g_bgRef[1][1];
    }
    g_bgRefDirty = 0;
    if (g_oamDirty) {
        dma3_cpy(oam_mem, g_oam, sizeof g_oam);
        g_oamDirty = false;
    }
    if (g_palDirty) {
        dma3_cpy(pal_bg_mem, g_pal, sizeof g_pal);
        g_palDirty = false;
    }
    // Queued writes in order, up to the frame's DMA budget; rows past it wait for the next VBlank (a row larger than the
    // whole budget goes first in a frame of its own).
    u32 left = g_budget;
    bool any = false;
    int done = 0;
    for (; done < g_queueLen; ++done) {
        Write& w = g_queue[done];
        while (w.rows && (w.rowBytes <= left || !any)) {
            writeRow(w);
            left = w.rowBytes < left ? left - w.rowBytes : 0;
            any = true;
        }
        if (w.rows) break;
    }
    if (done == g_queueLen) {
        g_queueLen = 0;
        g_queueBytes = 0;
        return;
    }
    g_queueBytes = 0;
    for (int i = done; i < g_queueLen; ++i) {
        g_queue[i - done] = g_queue[i];
        g_queueBytes += g_queue[i].rowBytes * g_queue[i].rows;
    }
    g_queueLen -= done;
    deferred();
}

void setReg(Reg r, u16 value) {
    if (g_regs[r] == value) return;
    g_regs[r] = value;
    g_regDirty |= 1u << r;
}

u16 reg(Reg r) { return g_regs[r]; }

void setBgRef(int bg, s32 x, s32 y) {
    const int i = bg == 3 ? 1 : 0;
    g_bgRef[i][0] = x;
    g_bgRef[i][1] = y;
    g_bgRefDirty |= static_cast<u8>(1 << i);
}

s32 bgRef(int bg, int axis) { return g_bgRef[bg == 3 ? 1 : 0][axis & 1]; }

ObjAttr* oam() {
    g_oamDirty = true;
    return g_oam;
}

void setAffine(int index, s16 pa, s16 pb, s16 pc, s16 pd) {
    g_oam[index * 4 + 0].fill = pa;
    g_oam[index * 4 + 1].fill = pb;
    g_oam[index * 4 + 2].fill = pc;
    g_oam[index * 4 + 3].fill = pd;
    g_oamDirty = true;
}

void markOamDirty() { g_oamDirty = true; }

u16* palette() {
    g_palDirty = true;
    return g_pal;
}

void markPaletteDirty() { g_palDirty = true; }

bool queueCopy(void* dst, const void* src, u32 bytes) {
    if (g_queueLen >= kDmaQueueSize || g_queueBytes + bytes > g_budget) {
        deferred();
        return false;
    }
    return push({static_cast<u8*>(dst), static_cast<const u8*>(src), bytes, 1, 0, 0, Kind::Copy});
}

bool queueFill(void* dst, u16 value, u32 rowBytes, u32 rows, u32 dstStride) {
    if (!rows || !rowBytes) return true;
    return push({static_cast<u8*>(dst), nullptr, rowBytes, static_cast<u16>(rows), static_cast<u16>(dstStride), value, Kind::Fill});
}

bool queueSequence(void* dst, u16 value, u32 rowEntries, u32 rows, u32 dstStride) {
    if (!rows || !rowEntries) return true;
    return push({static_cast<u8*>(dst), nullptr, rowEntries * 2, static_cast<u16>(rows), static_cast<u16>(dstStride), value, Kind::Sequence});
}

void setDmaBudget(u32 bytes) { g_budget = bytes; }
u32 queuedBytes() { return g_queueBytes; }
u32 deferredCount() { return g_deferred; }

void copyNow(void* dst, const void* src, u32 bytes) {
    checkDirect();
    dmaCopy(dst, src, bytes);
}

void fillNow(void* dst, u32 value, u32 bytes) {
    checkDirect();
    if (((reinterpret_cast<u32>(dst) | bytes) & 3) == 0)
        dma3_fill(dst, value, bytes);
    else
        for (u32 i = 0; i < bytes / 2; ++i) static_cast<u16*>(dst)[i] = static_cast<u16>(value);
}

void beginStagedLoad() { g_staged = true; }
void endStagedLoad() { g_staged = false; }
u32 directWrites() { return g_direct; }

void flushRegistersNow() {
    writeRegisters((1u << RegCount) - 1);
    g_regDirty = 0;
}

u16* bgTiles(int charBlock) { return reinterpret_cast<u16*>(MEM_VRAM + charBlock * 0x4000); }
u16* bgMap(int screenBlock) { return reinterpret_cast<u16*>(MEM_VRAM + screenBlock * 0x800); }
u16* objTiles() { return reinterpret_cast<u16*>(MEM_VRAM + 0x10000); }

}  // namespace agb::commit
