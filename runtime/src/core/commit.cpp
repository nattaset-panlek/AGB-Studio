#include "agb/commit.hpp"

#include "agb/hw.hpp"

#include "agb/debug.hpp"

namespace agb::commit {

namespace {
// One queued write of `rows` rows, `dstStride` bytes apart in VRAM: a copy reads each row from the caller's memory, a
// fill writes `value` to every halfword, a sequence writes value, value + 1 ... across the rows.
// bytes writes one byte per row (read-modify-write), pixels applies `rows` entries of g_pixels, page sets DISPCNT bit 4.
// romRows copies like copy, from ROM, whose slower reads cost half again as much of the frame's budget.
enum class Kind : u8 { Copy, Fill, Sequence, Bytes, Pixels, Page, RomRows };
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
struct Pixel {
    u16* dst;
    u16 value, mask;
};
static_assert(sizeof(Pixel) * kPixelQueueSize == kPixelQueueBytes);
AGB_EWRAM_BSS Pixel* g_pixels;  // lent by the bitmap scene (setPixelQueue)
AGB_EWRAM_BSS int g_pixelEnd;   // next free entry; back to 0 whenever the queue runs empty
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
        case Kind::RomRows:
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
        case Kind::Bytes: {
            volatile u16* h = reinterpret_cast<volatile u16*>(reinterpret_cast<u32>(w.dst) & ~1u);
            *h = (reinterpret_cast<u32>(w.dst) & 1) ? static_cast<u16>((*h & 0x00FF) | (w.value << 8)) : static_cast<u16>((*h & 0xFF00) | w.value);
            break;
        }
        case Kind::Pixels: {
            const Pixel& p = *reinterpret_cast<const Pixel*>(w.src);
            volatile u16* d = p.dst;
            *d = p.mask == 0xFFFF ? p.value : static_cast<u16>((*d & ~p.mask) | p.value);
            w.src += sizeof(Pixel);
            break;
        }
        case Kind::Page:
            g_regs[DispCnt] = static_cast<u16>((g_regs[DispCnt] & ~DCNT_PAGE) | w.value);
            REG_DISPCNT = g_regs[DispCnt];
            break;
    }
    w.dst += w.dstStride;
    --w.rows;
}

// What a row takes of the frame's budget: past it, the writes would run out of VBlank and show (tearing, a late flip).
u32 rowCost(const Write& w) { return w.kind == Kind::RomRows ? w.rowBytes + w.rowBytes / 2 : w.rowBytes; }

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
    g_pixelEnd = 0;
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
        const u32 cost = rowCost(w);
        while (w.rows && (cost <= left || !any)) {
            writeRow(w);
            left = cost < left ? left - cost : 0;
            any = true;
        }
        if (w.rows) break;
    }
    if (done == g_queueLen) {
        g_queueLen = 0;
        g_queueBytes = 0;
        g_pixelEnd = 0;
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
    if (g_queueLen >= kDmaQueueSize - kQueueReserve || g_queueBytes + bytes > g_budget) {
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

bool queueCopyRows(void* dst, const void* src, u32 rowBytes, u32 rows, u32 dstStride) {
    if (!rows || !rowBytes) return true;
    return push({static_cast<u8*>(dst), static_cast<const u8*>(src), rowBytes, static_cast<u16>(rows), static_cast<u16>(dstStride), 0, Kind::RomRows});
}

bool queueBytes(void* dst, u8 value, u32 rows, u32 dstStride) {
    if (!rows) return true;
    return push({static_cast<u8*>(dst), nullptr, 2, static_cast<u16>(rows), static_cast<u16>(dstStride), value, Kind::Bytes});
}

void setPixelQueue(void* memory) {
    g_pixels = static_cast<Pixel*>(memory);
    g_pixelEnd = 0;
}

bool queuePixel(u16* dst, u16 value, u16 mask) {
    if (!g_pixels || g_pixelEnd >= kPixelQueueSize) {
        debug::warnOnce(41, "more than 512 pixels were set in one frame, so the rest were dropped. Set fewer pixels per frame, or draw rectangles.");
        return false;
    }
    // The newest batch grows while nothing else was queued after it (its entries are next to each other).
    if (g_queueLen > 0 && g_queue[g_queueLen - 1].kind == Kind::Pixels) {
        ++g_queue[g_queueLen - 1].rows;
        g_queueBytes += kPixelBudgetBytes;
    } else if (!push({nullptr, reinterpret_cast<const u8*>(&g_pixels[g_pixelEnd]), kPixelBudgetBytes, 1, 0, 0, Kind::Pixels})) {
        return false;
    }
    g_pixels[g_pixelEnd++] = {dst, value, mask};
    return true;
}

bool queuePage(int page) {
    return push({nullptr, nullptr, 0, 1, 0, static_cast<u16>(page ? DCNT_PAGE : 0), Kind::Page});
}

void dropQueued() {
    g_queueLen = 0;
    g_queueBytes = 0;
    g_pixels = nullptr;
    g_pixelEnd = 0;
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
