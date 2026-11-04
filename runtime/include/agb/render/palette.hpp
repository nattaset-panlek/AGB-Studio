// Palette banks and effects (Palette Manager T-P10, Palette Effect T-O10).
// Base colours live in a buffer; effects (fade, tint, cycle, flash) are applied on top each frame and the
// result goes to the commit palette shadow. Bank masks: bits 0-15 = BG banks, bits 16-31 = OBJ banks.
#pragma once
#include "agb/data.hpp"
#include "agb/types.hpp"

namespace agb::render::palettes {

constexpr u32 kAllBg = 0x0000FFFF;
constexpr u32 kAllObj = 0xFFFF0000;
constexpr u32 kAll = 0xFFFFFFFF;

enum class Direction : u8 { Left, Right, PingPong };

void reset();
// Frees the OBJ banks only: a connected scene keeps the background's palettes and brings its own sprites.
void resetObj();
// Loads a palette into free banks (shared by pointer, reference counted). Returns the first bank or -1.
int acquire(const data::Palette* palette, bool obj);
void release(const data::Palette* palette, bool obj);
void setBank(bool obj, int bank, const u16* colors16);
u16 baseColor(int index);          // 0..511
void setBaseColor(int index, u16 bgr555);
void setBackdrop(u16 bgr555);
int banksUsed(bool obj);

// Fade the masked banks toward `color` over `frames` frames, ending at `amount` (0 = none, 16 = full).
void startFade(u32 mask, u16 color, u8 amount, u16 frames);
bool fading();
void setFade(u32 mask, u16 color, u8 amount);  // immediate
// Tint multiplies toward a colour by amount/16 (no animation).
void setTint(u32 mask, u16 color, u8 amount);
// Colour cycle: rotates indices first..last of one bank every `ticks` frames. Returns a slot id or -1.
int startCycle(bool obj, int bank, u8 first, u8 last, u8 ticks, Direction direction);
void stopCycle(int slot);
// Flash the masked banks to `color` for `frames` frames ("Flash on hit").
void flash(u32 mask, u16 color, u8 frames);

void update();  // advances effects; called once per frame by the frame loop of the game
// Indices that changed this frame (Debugger · Palettes "white outline = changed this frame").
const u32* changedThisFrame();  // 16 words = 512 bits

u16 blend(u16 a, u16 b, u8 amount16);

}  // namespace agb::render::palettes
