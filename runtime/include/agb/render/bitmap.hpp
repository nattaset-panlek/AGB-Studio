// Mode 3 bitmap (Video Mode "Mode 3 · 1 bitmap 240×160 · 16-bit colour"): BG2 shows a 240 x 160 frame buffer of
// BGR555 pixels at the start of VRAM. The frame buffer covers the background tile area and the first half of the
// sprite tile area, so in Mode 3 sprites use OBJ tiles 512..1023 (render::sprites does this) and no tile layers can
// be loaded. All writes go through the commit queue.
#pragma once
#include "agb/types.hpp"

namespace agb::render::bitmap {

constexpr int kWidth = 240, kHeight = 160;

// Switches to Mode 3 with BG2 on (it keeps BG2's mosaic and priority settings) and reserves the frame buffer in the
// background VRAM allocator. BG2 is shown 1:1 (its rotate / scale registers are reset).
void begin();
// Copies a whole picture (240 x 160 BGR555, row-major) into the frame buffer at once: a staged load, so call it while
// the screen is blank or faded out.
void show(const u16* pixels);
// Fills a rectangle at once (staged loads); the rectangle is clipped to the screen.
void fill(int x, int y, int width, int height, u16 color);
// Queues rows [firstRow, firstRow + rows) of a whole picture for the next VBlank. Returns false when the DMA budget
// deferred them (call again next frame).
bool queueRows(const u16* pixels, int firstRow, int rows);
// The frame buffer in VRAM (read it for tests; write it through show / fill / queueRows).
const u16* frameBuffer();

}  // namespace agb::render::bitmap
