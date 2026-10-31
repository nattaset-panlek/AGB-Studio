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
    const int w = image ? l.width : l.width * 2, h = image ? l.height : l.height * 2;
    if (l.wrap) {
        // The ARM7 has no divide instruction: wrap by subtraction (the camera is rarely more than a few map
        // widths from the origin).
        while (tileX >= w) tileX -= w;
        while (tileX < 0) tileX += w;
        while (tileY >= h) tileY -= h;
        while (tileY < 0) tileY += h;
    } else if (tileX < 0 || tileY < 0 || tileX >= w || tileY >= h) {
        return 0;
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
// One run of a metatile row: `n` entries from map column `tx` (inside the map), two per metatile.
void metatileRun(u16* d, const u16* row, int tx, int n, const data::Metatile* metas, u32 count, u32 quad, u32 offset) {
    const u16* c = row + (tx >> 1);
    int j = 0;
    if (tx & 1) {  // starts on the right half of a metatile
        const u32 mt = *c++;
        d[j++] = static_cast<u16>(mt < count ? metas[mt].se[quad + 1] + offset : 0);
    }
    for (; j + 1 < n; j += 2) {
        const u32 mt = *c++;
        if (mt < count) {
            const u16* e = metas[mt].se + quad;
            d[j] = static_cast<u16>(e[0] + offset);
            d[j + 1] = static_cast<u16>(e[1] + offset);
        } else {
            d[j] = d[j + 1] = 0;
        }
    }
    if (j < n) {
        const u32 mt = *c;
        d[j] = static_cast<u16>(mt < count ? metas[mt].se[quad] + offset : 0);
    }
}
}  // namespace

// Fast path for image layers (sky, hills) and tile layers without wrap: the row is written as straight runs of shadow
// entries, split where it crosses into the next 32-tile screen block (each run marks its block row dirty once) and where
// the source row wraps; an entry is the map's entry plus the layer's first tile and palette bank. A big camera move
// redraws the whole window this way at about 25 cycles a cell (P3.04).
void MapLayerView::drawRow(int tileY, int x0, int x1) {
    const data::MapLayer& l = *layer_;
    const bool image = l.type == data::LayerType::Image;
    if (!l.cells || !placeByAdd_ || (!image && (l.type != data::LayerType::Tile || l.wrap || !l.tileset))) {
        for (int x = x0; x <= x1; ++x) drawTile(x, tileY);
        return;
    }
    const bool wrap = l.wrap;
    const int w = image ? l.width : l.width * 2, h = image ? l.height : l.height * 2;  // in screen entries
    int ty = tileY;
    if (wrap) {
        while (ty >= h) ty -= h;
        while (ty < 0) ty += h;
    }
    // The source row; nullptr above or below a map that does not wrap (blank entries).
    const u16* src = ty >= 0 && ty < h ? l.cells + (image ? ty : ty >> 1) * l.width : nullptr;
    const data::Metatile* metas = image ? nullptr : l.tileset->metatiles;
    const u32 count = image ? 0 : l.tileset->metatileCount;
    const u32 quad = (ty & 1) * 2;
    const u32 offset = placement_.firstTile | (static_cast<u32>(placement_.paletteBank) << 12);
    const int y = tileY & heightMask_;
    const int blocksWide = (widthMask_ + 1) >> 5;
    u16* const rowBase = shadow_ + (y >> 5) * blocksWide * 1024 + (y & 31) * 32;
    u32* const dirty = dirtyRows_ + (y >> 5) * blocksWide;
    const u32 rowBit = 1u << (y & 31);
    int tx = x0;  // source column (screen entries)
    if (wrap) {
        while (tx >= w) tx -= w;
        while (tx < 0) tx += w;
    }
    for (int x = x0; x <= x1;) {
        const int col = x & widthMask_;
        int run = 32 - (col & 31);
        if (run > x1 - x + 1) run = x1 - x + 1;
        u16* dst = rowBase + (col >> 5) * 1024 + (col & 31);
        dirty[col >> 5] |= rowBit;
        x += run;
        for (int k = 0; k < run;) {
            if (!src || tx < 0 || tx >= w) {  // beside or above / below a map that does not wrap
                dst[k++] = 0;
                ++tx;
                continue;
            }
            int n = run - k;
            if (n > w - tx) n = w - tx;  // up to the source row's end (a wrapping row goes on from its start)
            u16* d = dst + k;
            if (image) {
                const u16* s = src + tx;
                for (int j = 0; j < n; ++j) d[j] = static_cast<u16>(s[j] + offset);
            } else {
                metatileRun(d, src, tx, n, metas, count, quad, offset);
            }
            k += n;
            tx += n;
            if (wrap && tx >= w) tx = 0;
        }
    }
}

}  // namespace agb::render
