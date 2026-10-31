// Screen effects: alpha blending, brightness, mosaic, windows, per-scanline tables (HBlank DMA) and
// scene transitions (Link Settings T-O09: Cut / Fade / Slide).
#pragma once
#include "agb/types.hpp"

namespace agb::render::effects {

// Layer bits for blend targets: BG0..BG3 = 1,2,4,8, OBJ = 16, backdrop = 32.
constexpr u8 kBg0 = 1, kBg1 = 2, kBg2 = 4, kBg3 = 8, kObj = 16, kBackdrop = 32;

void reset();
// Alpha blending (Video Mode "Alpha blending (fog, shadows)"): eva / evb in 0..16. It stays the blend of the screen: a
// fade or transition that uses the blend unit for brightness puts it back when it ends.
void setAlphaBlend(u8 firstTargets, u8 secondTargets, u8 eva, u8 evb);
// Brightness: amount 0..16, positive = toward white, negative = toward black.
void setBrightness(u8 targets, int amount);
void clearBlend();  // no blend, and none to put back
// Mosaic (Video Mode "Mosaic effect"): block size 1..16 for BG and OBJ. Only layers and sprites with their mosaic bit
// set take part (bg::Config::mosaic, sprites::setMosaic / setMosaicAll).
void setMosaic(u8 bgSize, u8 objSize);
// Transitions also grow the mosaic blocks (1 px when clear, 9 px when covered): the scene's "Mosaic effect".
void setTransitionMosaic(bool on);
// Window 0 rectangle; `inside` / `outside` are layer masks that stay visible.
void setWindow0(int left, int top, int right, int bottom, u8 inside, u8 outside);
void disableWindows();

// Per-scanline values written by HBlank DMA. The table holds 161 entries (lines 0..159 plus one spare
// read after the last line) of `wordsPerLine` 32-bit words copied each HBlank to the I/O offset, e.g.
// BG2 affine parameters for Mode 7 planes.
void startScanline(const u32* table, u16 ioOffset, u8 wordsPerLine);
void stopScanline();
bool scanlineActive();

enum class Transition : u8 { Cut, Fade, Slide };

// Starts a transition out (to black) over `frames`; update() advances it. Fade uses brightness,
// Slide closes a window from the right edge.
void beginOut(Transition t, u16 frames);
void beginIn(Transition t, u16 frames);
void update();
bool transitionDone();
int transitionLevel();  // 0 = clear, 16 = fully covered

}  // namespace agb::render::effects
