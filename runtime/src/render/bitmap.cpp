#include "agb/render/bitmap.hpp"

#include "agb/hw.hpp"

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/palette.hpp"

namespace agb::render::bitmap {

namespace {
AGB_EWRAM_BSS int g_shown;  // the page the last queued flip shows
AGB_EWRAM_BSS int g_draw;   // the page drawing goes to

int mode() { return bg::videoMode(); }

// Pages start at the first background character block (0x06000000); the second page is 40 KB on.
u16* page(int index) { return commit::bgTiles(0) + (index ? kSecondPageOffset / 2 : 0); }

// False, with a warning in the Console, while a tile mode is on: the frame buffers would land on tiles and maps.
bool usable() {
    if (mode() >= 3) return true;
    debug::warnOnce(42, "Bitmap drawing only works in Modes 3, 4 and 5, so this scene ignores it. Pick one of those modes in the Video Mode popover.");
    return false;
}

// Row by row: the audio interrupt can run between rows of a staged load.
void copyPage(u16* dst, const void* src, u32 rowBytes, int rows) {
    const u8* s = static_cast<const u8*>(src);
    u8* d = reinterpret_cast<u8*>(dst);
    for (int y = 0; y < rows; ++y) commit::copyNow(d + y * rowBytes, s + y * rowBytes, rowBytes);
}

void loadColors(const data::Picture& picture) {
    if (!picture.palette) return;
    for (int i = 0; i < picture.colors; ++i) palettes::setBaseColor(i + 1, picture.palette[i]);
}

// The picture's rows for the current mode: pointer and bytes per row.
const void* source(const data::Picture& picture, u32& rowBytes) {
    if (mode() == 4) {
        rowBytes = kWidth;
        return picture.indices;
    }
    rowBytes = static_cast<u32>(width()) * 2;
    return picture.bgr;
}
}  // namespace

void begin(int m) {
    commit::dropQueued();  // what the last scene left in the queue must not land on the picture
    commit::setPixelQueue(screenArena().alloc(commit::kPixelQueueBytes));
    bg::setVideoMode(m < 3 ? 3 : m);
    commit::setReg(commit::DispCnt, static_cast<u16>(commit::reg(commit::DispCnt) & ~DCNT_PAGE));
    g_shown = 0;
    g_draw = pageCount() - 1;
    // The bitmap is drawn through BG2's rotate / scale registers: show it 1:1 from the top-left corner.
    commit::setReg(commit::Bg2Pa, 256);
    commit::setReg(commit::Bg2Pb, 0);
    commit::setReg(commit::Bg2Pc, 0);
    commit::setReg(commit::Bg2Pd, 256);
    commit::setBgRef(2, 0, 0);
    // Up to 80 KB: every 2 KB background unit is taken, so no tileset or map can be placed over the picture.
    bg::Mark all = bg::mark();
    all.units = 0xFFFFFFFFu;
    bg::rewind(all);
    bg::setVisible(2, true);
    // Behind everything: sprites of any priority draw over the picture.
    bg::Config c = bg::config(2);
    c.priority = 3;
    bg::configure(2, c);
}

int width() { return mode() == 5 ? kMode5Width : kWidth; }
int height() { return mode() == 5 ? kMode5Height : kHeight; }
int pageCount() { return mode() >= 4 ? 2 : 1; }
int shownPage() { return g_shown; }
int drawPage() { return g_draw; }

void show(const u16* pixels) {
    if (usable()) copyPage(page(g_draw), pixels, static_cast<u32>(width()) * 2, height());
}

void showIndexed(const u8* indices) {
    if (usable() && mode() == 4) copyPage(page(g_draw), indices, kWidth, kHeight);
}

bool fits(const data::Picture& picture) {
    if (mode() == 4) return picture.indices && picture.width == kWidth && picture.height == kHeight;
    return mode() >= 3 && picture.bgr && picture.width == width() && picture.height == height();
}

bool showNow(const data::Picture& picture, int index) {
    if (!fits(picture)) return false;
    u32 rowBytes = 0;
    const void* src = source(picture, rowBytes);
    copyPage(page(index), src, rowBytes, height());
    if (mode() == 4) loadColors(picture);
    return true;
}

void clearNow(u16 color, int index) {
    if (!usable()) return;
    const u16 value = mode() == 4 ? static_cast<u16>((color & 0xFF) | (color << 8)) : color;
    const u32 rowBytes = mode() == 4 ? kWidth : static_cast<u32>(width()) * 2;
    u8* d = reinterpret_cast<u8*>(page(index));
    for (int y = 0; y < height(); ++y) commit::fillNow(d + y * rowBytes, value | (static_cast<u32>(value) << 16), rowBytes);
}

bool clear(u16 color) { return fill(0, 0, width(), height(), color); }

bool setPixel(int x, int y, u16 color) {
    if (!usable()) return true;
    if (x < 0 || y < 0 || x >= width() || y >= height()) return true;
    if (mode() == 4) {
        // No byte writes in VRAM: the queue changes one byte of the halfword in VBlank.
        const bool high = x & 1;
        const u16 index = color & 0xFF;
        return commit::queuePixel(page(g_draw) + (y * kWidth + x) / 2, high ? static_cast<u16>(index << 8) : index, high ? 0xFF00 : 0x00FF);
    }
    return commit::queuePixel(page(g_draw) + y * width() + x, color);
}

bool fill(int x, int y, int w, int h, u16 color) {
    if (!usable()) return true;
    int x0 = x < 0 ? 0 : x, x1 = x + w > width() ? width() : x + w;
    const int y0 = y < 0 ? 0 : y, y1 = y + h > height() ? height() : y + h;
    if (x0 >= x1 || y0 >= y1) return true;
    const u32 rows = static_cast<u32>(y1 - y0);
    if (mode() != 4) {
        const u32 stride = static_cast<u32>(width()) * 2;
        return commit::queueFill(page(g_draw) + y0 * width() + x0, color, static_cast<u32>(x1 - x0) * 2, rows, stride);
    }
    // Mode 4: the odd columns at the edges share a halfword with a pixel outside the rectangle.
    const u8 index = static_cast<u8>(color);
    u8* base = reinterpret_cast<u8*>(page(g_draw)) + y0 * kWidth;
    bool ok = true;
    if (x0 & 1) ok = commit::queueBytes(base + x0++, index, rows, kWidth) && ok;
    if (x0 < x1 && (x1 & 1)) ok = commit::queueBytes(base + --x1, index, rows, kWidth) && ok;
    if (x0 < x1) ok = commit::queueFill(base + x0, static_cast<u16>(index | (index << 8)), static_cast<u32>(x1 - x0), rows, kWidth) && ok;
    return ok;
}

bool queuePicture(const data::Picture& picture) {
    if (!usable()) return true;
    if (!fits(picture)) {
        debug::warnOnce(43, "Bitmap.Show: this picture has no data for the scene's video mode, or is another size, so it is not shown. Modes 3 and 4 show 240 x 160 pictures and Mode 5 shows 160 x 128; a picture for a script needs a scene in that mode.");
        return false;
    }
    u32 rowBytes = 0;
    const void* src = source(picture, rowBytes);
    if (mode() == 4) loadColors(picture);
    return commit::queueCopyRows(page(g_draw), src, rowBytes, static_cast<u32>(height()), rowBytes);
}

bool flip() {
    if (!usable() || pageCount() < 2) return true;
    if (!commit::queuePage(g_draw)) return false;
    g_shown = g_draw;
    g_draw ^= 1;
    return true;
}

void setPaletteColor(int index, u16 color) {
    if (index >= 1 && index <= 255) palettes::setBaseColor(index, color);
}

bool queueRows(const u16* pixels, int firstRow, int rows) {
    if (!usable() || firstRow < 0 || rows <= 0 || firstRow + rows > height()) return true;
    const int w = width();
    return commit::queueCopy(page(g_draw) + firstRow * w, pixels + firstRow * w, static_cast<u32>(rows * w) * 2);
}

const u16* frameBuffer() { return page(g_draw); }
const u16* pageBuffer(int index) { return page(index); }

}  // namespace agb::render::bitmap
