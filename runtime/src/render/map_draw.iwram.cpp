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

void MapLayerView::drawRow(int tileY, int x0, int x1) {
    const data::MapLayer& l = *layer_;
    // Fast path for image layers (sky, hills): one row of screen entries, wrapped once per row.
    if (l.type == data::LayerType::Image && l.cells) {
        const int w = l.width, h = l.height;
        int ty = tileY;
        bool rowInside = true;
        if (l.wrap) {
            while (ty >= h) ty -= h;
            while (ty < 0) ty += h;
        } else {
            rowInside = ty >= 0 && ty < h;
        }
        const u16* row = rowInside ? l.cells + ty * w : nullptr;
        const u16 tileBase = placement_.firstTile, palBase = placement_.paletteBank;
        int tx = x0;
        if (l.wrap) {
            while (tx >= w) tx -= w;
            while (tx < 0) tx += w;
        }
        for (int x = x0; x <= x1; ++x) {
            u16 se = 0;
            if (row && (l.wrap || (tx >= 0 && tx < w))) {
                const u16 raw = row[tx];
                se = static_cast<u16>(((data::seTile(raw) + tileBase) & 0x3FF) | (raw & (data::kSeHFlip | data::kSeVFlip)) |
                                      (((data::sePal(raw) + palBase) & 15) << 12));
            }
            if (++tx == w && l.wrap) tx = 0;
            const int i = shadowIndex(x, tileY);
            if (shadow_[i] == se) continue;
            shadow_[i] = se;
            dirtyRows_[(i >> 10) & 3] |= 1u << ((i >> 5) & 31);
        }
        return;
    }
    // Fast path for tile layers without wrap: one metatile row, no division per cell.
    if (l.type != data::LayerType::Tile || l.wrap || !l.tileset) {
        for (int x = x0; x <= x1; ++x) drawTile(x, tileY);
        return;
    }
    const int h = l.height * 2, w = l.width * 2;
    const bool rowInside = tileY >= 0 && tileY < h;
    const u16* cells = rowInside ? l.cells + (tileY >> 1) * l.width : nullptr;
    const data::Metatile* metas = l.tileset->metatiles;
    const u16 count = l.tileset->metatileCount;
    const int quadRow = (tileY & 1) * 2;
    const u16 tileBase = placement_.firstTile, palBase = placement_.paletteBank;
    for (int x = x0; x <= x1; ++x) {
        u16 se = 0;
        if (rowInside && x >= 0 && x < w) {
            const u16 mt = cells[x >> 1];
            if (mt < count) {
                const u16 raw = metas[mt].se[(x & 1) + quadRow];
                se = static_cast<u16>(((data::seTile(raw) + tileBase) & 0x3FF) | (raw & (data::kSeHFlip | data::kSeVFlip)) |
                                      (((data::sePal(raw) + palBase) & 15) << 12));
            }
        }
        const int i = shadowIndex(x, tileY);
        if (shadow_[i] == se) continue;
        shadow_[i] = se;
        dirtyRows_[(i >> 10) & 3] |= 1u << ((i >> 5) & 31);
    }
}

}  // namespace agb::render
