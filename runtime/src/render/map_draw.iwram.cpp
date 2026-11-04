// Map drawing inner loops, compiled as ARM code into IWRAM because of the .iwram.cpp suffix: a scene load
// draws every visible cell of up to four layers in one step, and scrolling draws a row or column per frame.
#include "agb/render/map.hpp"

namespace agb::render {

int MapLayerView::shadowIndex(int tileX, int tileY) const {
    const int x = tileX & widthMask_, y = tileY & heightMask_;
    // Multi-block layers store each 32x32 screen block contiguously.
    const int block = (x >> 5) + (y >> 5) * ((widthMask_ + 1) >> 5);
    return block * 1024 + (y & 31) * 32 + (x & 31);
}


u16 MapLayerView::screenEntry(int tileX, int tileY) const {
    const data::MapLayer& l = *layer_;
    const bool image = l.type == data::LayerType::Image;
    if (!image) {
        tileX -= originX_ >> 3;
        tileY -= originY_ >> 3;
    }
    const int w = image ? l.width : l.width * 2, h = image ? l.height : l.height * 2;
    if (l.wrap) {
        // The ARM7 has no divide instruction: wrap by subtraction (the camera is rarely more than a few map
        // widths from the origin).
        while (tileX >= w) tileX -= w;
        while (tileX < 0) tileX += w;
        while (tileY >= h) tileY -= h;
        while (tileY < 0) tileY += h;
    } else if (tileX < 0 || tileY < 0 || tileX >= w || tileY >= h) {
        return neighborEntry(tileX, tileY);
    }
    u16 se;
    if (image) {
        se = l.cells[tileY * l.width + tileX];
    } else {
        const u16 mt = l.cells[(tileY >> 1) * l.width + (tileX >> 1)];
        if (!l.tileset || mt >= l.tileset->metatileCount) return 0;
        se = l.tileset->metatiles[mt].se[(tileX & 1) + (tileY & 1) * 2];
    }
    const u16 tile = static_cast<u16>((data::seTile(se) + placement_.firstTile) & 0x3FF);
    const u16 pal = static_cast<u16>((data::sePal(se) + placement_.paletteBank) & 15);
    return static_cast<u16>(tile | (se & (data::kSeHFlip | data::kSeVFlip)) | (pal << 12));
}

void MapLayerView::drawTile(int tileX, int tileY) {
    const int i = shadowIndex(tileX, tileY);
    const u16 se = screenEntry(tileX, tileY);
    if (shadow_[i] == se) return;
    shadow_[i] = se;
    const int block = i >> 10, row = (i >> 5) & 31;
    dirtyRows_[block & 3] |= 1u << row;
}

void MapLayerView::drawColumn(int tileX, int y0, int y1) {
    for (int y = y0; y <= y1; ++y) drawTile(tileX, y);
}

namespace {
// The source of one map row for MapLayerView::drawRow, and the two ways to fill a run of shadow entries from it.
struct RowSource {
    const u16* cells;  // image layers: screen entries; tile layers: metatile indices
    const data::Metatile* metas;
    u32 count, quad;   // metatile count; 0 or 2 for the top or bottom half of the metatiles
    u32 offset;        // the layer's first tile and palette bank, added to every entry
};
using RunFill = void (*)(u16* d, int tx, int n, const RowSource& r);

void imageRun(u16* d, int tx, int n, const RowSource& r) {
    const u16* s = r.cells + tx;
    for (int j = 0; j < n; ++j) d[j] = static_cast<u16>(s[j] + r.offset);
}

void metatileRun(u16* d, int tx, int n, const RowSource& r) {
    for (int j = 0; j < n; ++j, ++tx) {
        const u32 mt = r.cells[tx >> 1];
        d[j] = static_cast<u16>(mt < r.count ? r.metas[mt].se[(tx & 1) + r.quad] + r.offset : 0);
    }
}

int wrapped(int v, int size) {
    while (v >= size) v -= size;
    while (v < 0) v += size;
    return v;
}
}  // namespace

// Fast path for image layers (sky, hills) and tile layers without wrap: the row is written as straight runs of shadow
// entries, split where it crosses into the next 32-tile screen block (each run marks its block row dirty once) and where
// the source row wraps; an entry is the map's entry plus the layer's first tile and palette bank. A big camera move
// redraws the whole window this way at about 30 cycles a cell (P3.04).
void MapLayerView::drawRow(int tileY, int x0, int x1) {
    const data::MapLayer& l = *layer_;
    const bool image = l.type == data::LayerType::Image;
    if (!l.cells || !placeByAdd_ || (!image && (l.type != data::LayerType::Tile || l.wrap || !l.tileset))) {
        for (int x = x0; x <= x1; ++x) drawTile(x, tileY);
        return;
    }
    const bool wrap = l.wrap;
    const int w = image ? l.width : l.width * 2, h = image ? l.height : l.height * 2;  // in screen entries
    // Image layers keep to the hardware map's space; a tile layer's map may have moved in it (moveOrigin).
    const int ox = image ? 0 : originX_ >> 3, oy = image ? 0 : originY_ >> 3;
    const int ty = wrap ? wrapped(tileY, h) : tileY - oy;
    const bool inside = ty >= 0 && ty < h;  // rows above or below a map that does not wrap are blank
    RowSource r{l.cells + (inside ? (image ? ty : ty >> 1) * l.width : 0), image ? nullptr : l.tileset->metatiles,
                image ? 0u : l.tileset->metatileCount, static_cast<u32>(ty & 1) * 2,
                placement_.firstTile | (static_cast<u32>(placement_.paletteBank) << 12)};
    const RunFill fill = image ? imageRun : metatileRun;
    const int y = tileY & heightMask_;
    const int blocksWide = (widthMask_ + 1) >> 5;
    u16* const rowBase = shadow_ + (y >> 5) * blocksWide * 1024 + (y & 31) * 32;
    int tx = wrap ? wrapped(x0, w) : x0 - ox;  // source column (screen entries)
    for (int x = x0; x <= x1;) {
        const int col = x & widthMask_;
        int run = 32 - (col & 31);
        if (run > x1 - x + 1) run = x1 - x + 1;
        u16* dst = rowBase + (col >> 5) * 1024 + (col & 31);
        dirtyRows_[(y >> 5) * blocksWide + (col >> 5)] |= 1u << (y & 31);
        x += run;
        for (int k = 0; k < run;) {
            if (!inside || tx < 0 || tx >= w) {  // beside a map that does not wrap
                dst[k++] = neighborEntry(tx, ty);
                ++tx;
                continue;
            }
            const int n = run - k < w - tx ? run - k : w - tx;  // up to the row's end (a wrapping row goes on from 0)
            fill(dst + k, tx, n, r);
            k += n;
            tx += n;
            if (wrap && tx >= w) tx = 0;
        }
    }
}

}  // namespace agb::render
