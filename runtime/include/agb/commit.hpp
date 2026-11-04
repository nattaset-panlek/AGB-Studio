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
constexpr int kQueueReserve = 16;  // entries queueCopy leaves free for fills and sequences, which are never retried
constexpr int kPixelQueueSize = 512;       // queuePixel writes waiting at once
constexpr u32 kPixelQueueBytes = kPixelQueueSize * 8;  // the memory setPixelQueue takes
constexpr u32 kPixelBudgetBytes = 16;      // what one costs against the DMA budget: a CPU read-modify-write is slower than DMA
constexpr u32 kBitmapDmaBudget = 40 * 1024;  // scenes in modes 3-5: a whole Mode 4 or Mode 5 page in one VBlank (a fill of it takes about half of VBlank)

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
// the caller retries next frame. `src` must stay valid until that VBlank.
bool queueCopy(void* dst, const void* src, u32 bytes);
// Queued writes the commit keeps whole: the caller keeps no source and never retries. A rectangle is `rows` rows of
// `rowBytes` bytes, `dstStride` bytes apart (64 for a 32-wide screen block, 480 for the Mode 3 frame buffer, 240 in Mode 4). Rows that
// do not fit this frame's budget wait for the next VBlank (counted in deferredCount, logged once). They return false
// only when the queue is full (logged once).
// Fills every halfword of the rectangle with `value`.
bool queueFill(void* dst, u16 value, u32 rowBytes, u32 rows = 1, u32 dstStride = 0);
// Writes value, value + 1, value + 2 ... row after row: a block of tiles mapped in order (text windows).
bool queueSequence(void* dst, u16 value, u32 rowEntries, u32 rows, u32 dstStride);
// Copies a rectangle row by row from memory that stays valid (ROM): the commit keeps it whole, like a fill, and rows past
// the budget wait for the next VBlank.
bool queueCopyRows(void* dst, const void* src, u32 rowBytes, u32 rows, u32 dstStride);
// VRAM has no byte writes (a byte write lands in both bytes of the halfword). These read, change and write the halfword
// in VBlank, in queue order, so two writes to one halfword in a frame both stay.
// One byte at `dst` (any address), then the byte `dstStride` further on, `rows` times: the odd edge of a Mode 4 rectangle.
bool queueBytes(void* dst, u8 value, u32 rows, u32 dstStride);
// One small write: the bits of `mask` in `*dst` become `value`. Up to kPixelQueueSize a frame (Mode 3, 4, 5 set pixel);
// false when they are used up, and the write is dropped (logged once). Only bitmap scenes need it, so they lend the queue
// its kPixelQueueBytes of memory (from the screen arena); dropQueued takes it back.
void setPixelQueue(void* memory);
bool queuePixel(u16* dst, u16 value, u16 mask = 0xFFFF);
// Shows bitmap page 0 or 1 (DISPCNT bit 4) when the queue reaches it: a page drawn with queued writes is complete when
// it appears, however many VBlanks they took.
bool queuePage(int page);
// Forgets every queued write (a bitmap scene starts: writes left over from the last scene would land on its picture).
void dropQueued();
void setDmaBudget(u32 bytes);
u32 queuedBytes();  // bytes waiting for the next VBlank, rows left over from earlier frames included
u32 deferredCount();

// Immediate copy for staged loads: while the screen is blanked or faded to black (the scene loader opens that window
// with beginStagedLoad / endStagedLoad around each load step), or in VBlank. Debug builds count the calls made while
// the picture shows in directWrites() and log the first one.
void copyNow(void* dst, const void* src, u32 bytes);
void fillNow(void* dst, u32 value, u32 bytes);
void beginStagedLoad();
void endStagedLoad();
u32 directWrites();
// Writes all shadow registers immediately (used at boot and inside staged loads).
void flushRegistersNow();

// VRAM addresses for queue targets.
u16* bgTiles(int charBlock);
u16* bgMap(int screenBlock);
u16* objTiles();

}  // namespace agb::commit
