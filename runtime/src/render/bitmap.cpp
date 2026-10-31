#include "agb/render/bitmap.hpp"

#include "agb/commit.hpp"
#include "agb/render/bg.hpp"

namespace agb::render::bitmap {

namespace {
// The frame buffer starts at the first background character block (0x06000000).
u16* buffer() { return commit::bgTiles(0); }
}  // namespace

void begin() {
    bg::setVideoMode(3);
    // The bitmap is drawn through BG2's rotate / scale registers: show it 1:1 from the top-left corner.
    commit::setReg(commit::Bg2Pa, 256);
    commit::setReg(commit::Bg2Pb, 0);
    commit::setReg(commit::Bg2Pc, 0);
    commit::setReg(commit::Bg2Pd, 256);
    commit::setBgRef(2, 0, 0);
    // 76,800 bytes: every 2 KB background unit is taken, so no tileset or map can be placed over the picture.
    bg::Mark all = bg::mark();
    all.units = 0xFFFFFFFFu;
    bg::rewind(all);
    bg::setVisible(2, true);
}

void show(const u16* pixels) { commit::copyNow(buffer(), pixels, kWidth * kHeight * 2); }

bool fill(int x, int y, int width, int height, u16 color) {
    const int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
    const int x1 = x + width > kWidth ? kWidth : x + width, y1 = y + height > kHeight ? kHeight : y + height;
    if (x0 >= x1 || y0 >= y1) return true;
    return commit::queueFill(buffer() + y0 * kWidth + x0, color, static_cast<u32>(x1 - x0) * 2, static_cast<u32>(y1 - y0), kWidth * 2);
}

bool queueRows(const u16* pixels, int firstRow, int rows) {
    if (firstRow < 0 || rows <= 0 || firstRow + rows > kHeight) return true;
    return commit::queueCopy(buffer() + firstRow * kWidth, pixels + firstRow * kWidth, static_cast<u32>(rows) * kWidth * 2);
}

const u16* frameBuffer() { return buffer(); }

}  // namespace agb::render::bitmap
