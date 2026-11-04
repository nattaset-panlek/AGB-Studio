// Bitmap video modes: BG2 shows a frame buffer at the start of VRAM.
//   Mode 3: one page, 240 x 160, BGR555.
//   Mode 4: two pages, 240 x 160, 8-bit indices into the background palette. Index 0 is see-through (the backdrop
//           shows), so a picture has 255 colours, in palette indices 1..255. Pages at 0x06000000 and 0x0600A000.
//   Mode 5: two pages, 160 x 128, BGR555, at the same addresses. Shown 1:1 from the top-left corner of the screen; the
//           rest of the screen is the backdrop, as on the hardware.
// The frame buffers cover the background tile area and the first half of the sprite tile area, so in these modes
// sprites use OBJ tiles 512..1023 (render::sprites does this) and no tile layers or text can be loaded.
//
// In Modes 4 and 5 drawing goes to the hidden page and flip shows it; in Mode 3 drawing shows as it happens. Every call
// here is queued for VBlank through the commit queues, in order, except the staged loads (showNow, clearNow, show).
// A queued flip waits for the writes queued before it, so a page is whole when it appears.
#pragma once
#include "agb/data.hpp"
#include "agb/types.hpp"

namespace agb::render::bitmap {

constexpr int kWidth = 240, kHeight = 160;              // Modes 3 and 4
constexpr int kMode5Width = 160, kMode5Height = 128;
constexpr u32 kSecondPageOffset = 0xA000;               // bytes from the first page to the second

// Switches to a bitmap mode (3, 4 or 5) with BG2 on at priority 3 behind every sprite (it keeps BG2's mosaic setting),
// reserves the frame buffers in the background VRAM allocator, shows page 0 and forgets queued writes left from before.
// BG2 is shown 1:1 (its rotate / scale registers are reset).
void begin(int mode = 3);
// The current mode's picture size and page count (1 in Mode 3, 2 in Modes 4 and 5).
int width();
int height();
int pageCount();
int shownPage();  // the page the flips queued so far end on
int drawPage();   // the page drawing goes to

// Staged loads: at once, so call them while the screen is blank or faded out.
// A whole picture (BGR555, row-major, the mode's size) into the page drawing goes to.
void show(const u16* pixels);
// A whole Mode 4 picture (one index per pixel, row-major, word aligned) into the page drawing goes to.
void showIndexed(const u8* indices);
// A picture into one page; in Mode 4 also its colours into palette indices 1... False (and nothing drawn) when the
// picture has no data for the current mode or is another size.
bool showNow(const data::Picture& picture, int page);
// Fills one page with a colour (Mode 4: a palette index).
void clearNow(u16 color, int page);
bool fits(const data::Picture& picture);

// Queued for VBlank. `color` is BGR555 in Modes 3 and 5 and a palette index in Mode 4. They do nothing in the tile
// modes (logged once). Everything is clipped to the mode's picture size.
bool clear(u16 color);
bool setPixel(int x, int y, u16 color);  // at most commit::kPixelQueueSize a frame
// A filled rectangle; rows past the frame's DMA budget are filled in the VBlanks after it.
bool fill(int x, int y, int width, int height, u16 color);
// A whole picture row by row (from ROM) into the page drawing goes to, over a few VBlanks. In Mode 4 its colours replace
// the palette at once, and both pages use the palette: show it under a fade, or use pictures that share colours.
bool queuePicture(const data::Picture& picture);
// Shows the page just drawn; drawing then goes to the other page. Nothing happens in Mode 3.
bool flip();
// Mode 4: sets palette index 1..255 (index 0 is the backdrop, which is see-through).
void setPaletteColor(int index, u16 color);
// Queues rows [firstRow, firstRow + rows) of a whole BGR555 picture for the next VBlank. Returns false when the DMA
// budget deferred them (call again next frame).
bool queueRows(const u16* pixels, int firstRow, int rows);
// The frame buffer drawing goes to, and any page (read them for tests; write them through the calls above).
const u16* frameBuffer();
const u16* pageBuffer(int page);

}  // namespace agb::render::bitmap
