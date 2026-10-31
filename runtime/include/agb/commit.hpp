// Commit queues: the only path from game code to video hardware. Game code writes shadow copies
// and queues transfers during VDraw; flush() copies them in VBlank so the picture never tears.
#pragma once
#include "agb/types.hpp"

namespace agb::commit {

struct ObjAttr {
    u16 attr0, attr1, attr2;
    s16 fill;  // affine parameter slot (pa/pb/pc/pd interleave with OAM entries)
};

// Display registers mirrored in shadow copies (index into Registers::values).
enum Reg : u8 {
    DispCnt, Bg0Cnt, Bg1Cnt, Bg2Cnt, Bg3Cnt,
    Bg0HOfs, Bg0VOfs, Bg1HOfs, Bg1VOfs, Bg2HOfs, Bg2VOfs, Bg3HOfs, Bg3VOfs,
    Bg2Pa, Bg2Pb, Bg2Pc, Bg2Pd, Bg3Pa, Bg3Pb, Bg3Pc, Bg3Pd,
    Win0H, Win1H, Win0V, Win1V, WinIn, WinOut, Mosaic, BldCnt, BldAlpha, BldY,
    RegCount
};

constexpr u32 kDefaultDmaBudget = 16 * 1024;  // bytes per VBlank
constexpr int kDmaQueueSize = 64;

void init();
void flush();  // called by system::waitVBlank only

// Registers (32-bit values for the affine reference points are set with setBgRef).
void setReg(Reg r, u16 value);
u16 reg(Reg r);
void setBgRef(int bg, s32 x, s32 y);  // BG2 / BG3 affine reference point (24.8 screen coordinates)
s32 bgRef(int bg, int axis);           // shadow value of setBgRef: axis 0 = x, 1 = y

// OAM shadow: 128 entries; entries not written this frame keep their previous value.
ObjAttr* oam();
void setAffine(int index, s16 pa, s16 pb, s16 pc, s16 pd);
void markOamDirty();

// Palette shadow: 0..255 background, 256..511 objects (BGR555).
u16* palette();
void markPaletteDirty();

// Queues a VRAM copy for the next VBlank. Returns false (and logs once) when the budget is used up;
// the caller retries next frame.
bool queueCopy(void* dst, const void* src, u32 bytes);
void setDmaBudget(u32 bytes);
u32 queuedBytes();
u32 deferredCount();

// Immediate copy for staged loads while the screen is blanked or faded to black.
void copyNow(void* dst, const void* src, u32 bytes);
void fillNow(void* dst, u32 value, u32 bytes);
// Writes all shadow registers immediately (used at boot and inside staged loads).
void flushRegistersNow();

// VRAM addresses for queue targets.
u16* bgTiles(int charBlock);
u16* bgMap(int screenBlock);
u16* objTiles();

}  // namespace agb::commit
