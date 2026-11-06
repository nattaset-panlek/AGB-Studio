// GBA hardware: registers, memory, bit fields and BIOS calls the runtime uses (P24.03). Written from the GBA's documented
// hardware (I/O map at 0x04000000, BIOS SWI numbers) in place of libtonc's headers, which carry no licence to ship them.
// The names are the usual GBATEK-style names, so code reads the same as before.
#pragma once
#include <cstdint>

using u8 = uint8_t;
using u16 = uint16_t;
using u32 = uint32_t;
using s16 = int16_t;
using s32 = int32_t;
using vu16 = volatile uint16_t;
using vu32 = volatile uint32_t;
using vs32 = volatile int32_t;
using COLOR = uint16_t;

// ---- memory ------------------------------------------------------------------------------------------------
#define MEM_IO       0x04000000
#define MEM_PAL      0x05000000
#define MEM_VRAM     0x06000000
#define MEM_OAM      0x07000000
#define MEM_VRAM_OBJ (MEM_VRAM + 0x10000)
#define REG_BASE     MEM_IO

#define pal_bg_mem ((COLOR*)MEM_PAL)
#define vid_mem    ((COLOR*)MEM_VRAM)
#define oam_mem    ((u32*)MEM_OAM)

#define SCREEN_WIDTH  240
#define SCREEN_HEIGHT 160

#define RGB15(r, g, b) ((COLOR)((r) | ((g) << 5) | ((b) << 10)))

// ---- display -----------------------------------------------------------------------------------------------
#define REG_DISPCNT  (*(vu32*)(REG_BASE + 0x0000))
#define REG_DISPSTAT (*(vu16*)(REG_BASE + 0x0004))
#define REG_VCOUNT   (*(vu16*)(REG_BASE + 0x0006))
#define REG_BG2X     (*(vs32*)(REG_BASE + 0x0028))
#define REG_BG2Y     (*(vs32*)(REG_BASE + 0x002C))
#define REG_BG3X     (*(vs32*)(REG_BASE + 0x0038))
#define REG_BG3Y     (*(vs32*)(REG_BASE + 0x003C))
#define REG_BLDALPHA (*(vu16*)(REG_BASE + 0x0052))

#define DCNT_MODE3  0x0003
#define DCNT_PAGE   0x0010
#define DCNT_OBJ_1D 0x0040
#define DCNT_BLANK  0x0080
#define DCNT_BG0    0x0100
#define DCNT_BG1    0x0200
#define DCNT_BG2    0x0400
#define DCNT_BG3    0x0800
#define DCNT_OBJ    0x1000
#define DCNT_WIN0   0x2000
#define DCNT_WIN1   0x4000
#define DCNT_WINOBJ 0x8000

#define BG_PRIO(n)   (n)
#define BG_CBB(n)    ((n) << 2)
#define BG_MOSAIC    0x0040
#define BG_8BPP      0x0080
#define BG_SBB(n)    ((n) << 8)
#define BG_WRAP      0x2000
#define BG_REG_64x32 0x4000

#define BLD_STD   0x0040
#define BLD_WHITE 0x0080
#define BLD_BLACK 0x00C0

#define ATTR0_AFF       0x0100
#define ATTR0_HIDE      0x0200
#define ATTR0_AFF_DBL   0x0300
#define ATTR0_MODE_MASK 0x0300
#define ATTR0_BLEND     0x0400
#define ATTR0_MOSAIC    0x1000
#define ATTR1_HFLIP     0x1000
#define ATTR1_VFLIP     0x2000
#define ATTR2_PRIO(n)    ((n) << 10)
#define ATTR2_PALBANK(n) ((n) << 12)

// ---- sound -------------------------------------------------------------------------------------------------
#define REG_SND1SWEEP (*(vu16*)(REG_BASE + 0x0060))
#define REG_SND1CNT   (*(vu16*)(REG_BASE + 0x0062))
#define REG_SND1FREQ  (*(vu16*)(REG_BASE + 0x0064))
#define REG_SND2CNT   (*(vu16*)(REG_BASE + 0x0068))
#define REG_SND2FREQ  (*(vu16*)(REG_BASE + 0x006C))
#define REG_SND3SEL   (*(vu16*)(REG_BASE + 0x0070))
#define REG_SND3CNT   (*(vu16*)(REG_BASE + 0x0072))
#define REG_SND3FREQ  (*(vu16*)(REG_BASE + 0x0074))
#define REG_SND4CNT   (*(vu16*)(REG_BASE + 0x0078))
#define REG_SND4FREQ  (*(vu16*)(REG_BASE + 0x007C))
#define REG_SNDDMGCNT (*(vu16*)(REG_BASE + 0x0080))
#define REG_SNDDSCNT  (*(vu16*)(REG_BASE + 0x0082))
#define REG_SNDSTAT   (*(vu16*)(REG_BASE + 0x0084))

#define SSTAT_ENABLE 0x0080
#define SDS_DMG100   0x0002
#define SDMG_SQR1    0x01
#define SDMG_SQR2    0x02
#define SDMG_WAVE    0x04
#define SDMG_NOISE   0x08
// SOUNDCNT_L: right volume bits 0-2, left 4-6, right channels 8-11, left channels 12-15.
#define SDMG_BUILD(lmode, rmode, lvol, rvol) ((((lmode) & 15) << 12) | (((rmode) & 15) << 8) | (((lvol) & 7) << 4) | ((rvol) & 7))
#define SDMG_BUILD_LR(mode, vol) SDMG_BUILD(mode, mode, vol, vol)
#define SFREQ_RESET 0x8000

// ---- DMA ---------------------------------------------------------------------------------------------------
#define REG_DMA0SAD (*(vu32*)(REG_BASE + 0x00B0))
#define REG_DMA0DAD (*(vu32*)(REG_BASE + 0x00B4))
#define REG_DMA0CNT (*(vu32*)(REG_BASE + 0x00B8))
#define REG_DMA3SAD (*(vu32*)(REG_BASE + 0x00D4))
#define REG_DMA3DAD (*(vu32*)(REG_BASE + 0x00D8))
#define REG_DMA3CNT (*(vu32*)(REG_BASE + 0x00DC))

// DMAxCNT as one 32-bit write: word count in the low half, control in the high half.
#define DMA_DST_RELOAD 0x00600000
#define DMA_SRC_FIXED  0x01000000
#define DMA_REPEAT     0x02000000
#define DMA_16         0x00000000
#define DMA_32         0x04000000
#define DMA_AT_HBLANK  0x20000000
#define DMA_ENABLE     0x80000000

// Copies and fills in 32-bit units with DMA 3 (sizes in bytes, multiples of 4).
inline void dma3_cpy(void* dst, const void* src, u32 bytes) {
    REG_DMA3CNT = 0;
    REG_DMA3SAD = (u32)src;
    REG_DMA3DAD = (u32)dst;
    REG_DMA3CNT = (bytes >> 2) | DMA_32 | DMA_ENABLE;
}
inline void dma3_fill(void* dst, u32 value, u32 bytes) {
    static volatile u32 s_value;
    s_value = value;
    REG_DMA3CNT = 0;
    REG_DMA3SAD = (u32)&s_value;
    REG_DMA3DAD = (u32)dst;
    REG_DMA3CNT = (bytes >> 2) | DMA_SRC_FIXED | DMA_32 | DMA_ENABLE;
}

// ---- timers, keys, system ----------------------------------------------------------------------------------
#define REG_TM0CNT_L (*(vu16*)(REG_BASE + 0x0100))
#define REG_TM0CNT_H (*(vu16*)(REG_BASE + 0x0102))
#define REG_TM1CNT_L (*(vu16*)(REG_BASE + 0x0104))
#define REG_TM1CNT_H (*(vu16*)(REG_BASE + 0x0106))
#define REG_TM2CNT_L (*(vu16*)(REG_BASE + 0x0108))
#define REG_TM2CNT_H (*(vu16*)(REG_BASE + 0x010A))
#define REG_TM3CNT_L (*(vu16*)(REG_BASE + 0x010C))
#define REG_TM3CNT_H (*(vu16*)(REG_BASE + 0x010E))
#define TM_CASCADE 0x0004
#define TM_IRQ     0x0040
#define TM_ENABLE  0x0080

#define REG_KEYINPUT (*(vu16*)(REG_BASE + 0x0130))
#define REG_IE       (*(vu16*)(REG_BASE + 0x0200))
#define REG_IF       (*(vu16*)(REG_BASE + 0x0202))
#define REG_WAITCNT  (*(vu16*)(REG_BASE + 0x0204))
#define REG_IME      (*(vu16*)(REG_BASE + 0x0208))
// ROM wait states 3,1 with the prefetch buffer on, and SRAM 4: the setting every cartridge game uses.
#define WS_STANDARD 0x4317

// ---- interrupts (irq.iwram.cpp) ----------------------------------------------------------------------------
enum IrqIndex { II_VBLANK = 0, II_HBLANK, II_VCOUNT, II_TIMER0, II_TIMER1, II_TIMER2, II_TIMER3, II_SERIAL, II_DMA0, II_DMA1,
                II_DMA2, II_DMA3, II_KEYPAD, II_GAMEPAK, II_MAX };
using IrqHandler = void (*)();
// Installs the interrupt dispatcher, with no handlers, and turns interrupts on. `isr` must be null (kept for the
// old signature).
void irq_init(IrqHandler isr);
// Sets the handler of one interrupt (null: none) and turns that interrupt on at its source and in REG_IE.
void irq_add(IrqIndex index, IrqHandler handler);

// ---- BIOS calls --------------------------------------------------------------------------------------------
// SWI numbers: the BIOS reads them from the instruction; ARM code puts them in bits 16-23.
#if defined(__thumb__)
#define AGB_SWI(n) "swi " #n
#else
#define AGB_SWI(n) "swi (" #n " << 16)"
#endif

inline void Halt() { asm volatile(AGB_SWI(0x02) ::: "r0", "r1", "r2", "r3", "memory"); }
inline void VBlankIntrWait() { asm volatile(AGB_SWI(0x05) ::: "r0", "r1", "r2", "r3", "memory"); }
inline s32 Div(s32 num, s32 den) {
    register s32 r0 asm("r0") = num;
    register s32 r1 asm("r1") = den;
    asm volatile(AGB_SWI(0x06) : "+r"(r0), "+r"(r1) : : "r2", "r3");
    return r0;
}
inline s32 DivMod(s32 num, s32 den) {
    register s32 r0 asm("r0") = num;
    register s32 r1 asm("r1") = den;
    asm volatile(AGB_SWI(0x06) : "+r"(r0), "+r"(r1) : : "r2", "r3");
    return r1;
}
// CpuFastSet copies in units of 32 bytes; `mode` is the word count (bit 24 set would fill).
inline void CpuFastSet(const void* src, void* dst, u32 mode) {
    register const void* r0 asm("r0") = src;
    register void* r1 asm("r1") = dst;
    register u32 r2 asm("r2") = mode;
    asm volatile(AGB_SWI(0x0C) : "+r"(r0), "+r"(r1), "+r"(r2) : : "r3", "memory");
}
inline void LZ77UnCompWram(const void* src, void* dst) {
    register const void* r0 asm("r0") = src;
    register void* r1 asm("r1") = dst;
    asm volatile(AGB_SWI(0x11) : "+r"(r0), "+r"(r1) : : "r2", "r3", "memory");
}
inline void LZ77UnCompVram(const void* src, void* dst) {
    register const void* r0 asm("r0") = src;
    register void* r1 asm("r1") = dst;
    asm volatile(AGB_SWI(0x12) : "+r"(r0), "+r"(r1) : : "r2", "r3", "memory");
}
