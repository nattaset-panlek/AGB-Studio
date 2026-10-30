#include "agb/render/map.hpp"

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/memory.hpp"

namespace agb::render {

namespace {
// Visible area plus one tile on each side.
constexpr int kViewTilesX = 32, kViewTilesY = 22;

int floorDiv8(int v) { return v >> 3; }
}  // namespace

bool MapLayerView::init(const data::MapLayer& layer, bg::Size size) {
    layer_ = &layer;
    size_ = size;
    widthMask_ = bg::widthTiles(size) - 1;
    heightMask_ = bg::heightTiles(size) - 1;
    if (layer.type == data::LayerType::CollisionOnly) return true;
    if (layer.tileset) placement_ = bg::loadTileset(layer.tileset);
    if (layer.tileset && !placement_.valid()) return false;
    screenBlock_ = bg::allocScreenBlocks(size);
    if (screenBlock_ < 0) return false;
    const int cells = (widthMask_ + 1) * (heightMask_ + 1);
    shadow_ = static_cast<u16*>(screenArena().alloc(static_cast<u32>(cells) * 2));
    if (!shadow_) return false;
    for (int i = 0; i < cells; ++i) shadow_[i] = 0;
    bg::Config c;
    c.charBlock = static_cast<u8>(placement_.charBlock < 0 ? 0 : placement_.charBlock);
    c.screenBlock = static_cast<u8>(screenBlock_);
    c.priority = layer.priority;
    c.size = size;
    bg::configure(layer.bg, c);
    bg::setVisible(layer.bg, true);
    drawnX1_ = drawnY1_ = -1;
    return true;
}

void MapLayerView::release() {
    if (layer_ && layer_->type != data::LayerType::CollisionOnly) bg::setVisible(layer_->bg, false);
    layer_ = nullptr;
    shadow_ = nullptr;
}

int MapLayerView::shadowIndex(int tileX, int tileY) const {
    const int x = tileX & widthMask_, y = tileY & heightMask_;
    // Multi-block layers store each 32x32 screen block contiguously.
    const int block = (x >> 5) + (y >> 5) * ((widthMask_ + 1) >> 5);
    return block * 1024 + (y & 31) * 32 + (x & 31);
}

u16 MapLayerView::cell(int tileX, int tileY) const { return shadow_ ? shadow_[shadowIndex(tileX, tileY)] : 0; }

u16 MapLayerView::screenEntry(int tileX, int tileY) const {
    const data::MapLayer& l = *layer_;
    const bool image = l.type == data::LayerType::Image;
    const int w = image ? l.width : l.width * 2, h = image ? l.height : l.height * 2;
    if (l.wrap) {
        tileX = ((tileX % w) + w) % w;
        tileY = ((tileY % h) + h) % h;
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
    for (int x = x0; x <= x1; ++x) drawTile(x, tileY);
}

void MapLayerView::setCamera(int x, int y) {
    if (!layer_ || layer_->type == data::LayerType::CollisionOnly) return;
    cameraX_ = x;
    cameraY_ = y;
    scrollX_ = (x * layer_->parallaxX) >> 8;
    scrollY_ = (y * layer_->parallaxY) >> 8;
    bg::setScroll(layer_->bg, scrollX_, scrollY_);
    const int nx0 = floorDiv8(scrollX_) - 1, ny0 = floorDiv8(scrollY_) - 1;
    const int nx1 = nx0 + kViewTilesX, ny1 = ny0 + kViewTilesY;
    if (drawnX1_ < drawnX0_ || nx0 > drawnX1_ || nx1 < drawnX0_ || ny0 > drawnY1_ || ny1 < drawnY0_) {
        for (int ty = ny0; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    } else {
        // Only the columns and rows that entered the window.
        for (int tx = nx0; tx < drawnX0_; ++tx) drawColumn(tx, ny0, ny1);
        for (int tx = drawnX1_ + 1; tx <= nx1; ++tx) drawColumn(tx, ny0, ny1);
        for (int ty = ny0; ty < drawnY0_; ++ty) drawRow(ty, nx0, nx1);
        for (int ty = drawnY1_ + 1; ty <= ny1; ++ty) drawRow(ty, nx0, nx1);
    }
    drawnX0_ = nx0;
    drawnX1_ = nx1;
    drawnY0_ = ny0;
    drawnY1_ = ny1;
}

void MapLayerView::drawAll() {
    if (!layer_ || !shadow_) return;
    drawnX1_ = drawnY1_ = -1;
    setCamera(cameraX_, cameraY_);
    const int blocks = bg::screenBlockCount(size_);
    commit::copyNow(commit::bgMap(screenBlock_), shadow_, static_cast<u32>(blocks) * 2048);
    for (auto& w : dirtyRows_) w = 0;
}

bool MapLayerView::commit() {
    if (!shadow_) return true;
    bool all = true;
    const int blocks = bg::screenBlockCount(size_);
    for (int b = 0; b < blocks; ++b) {
        u32& rows = dirtyRows_[b];
        int r = 0;
        while (rows && r < 32) {
            if (!(rows & (1u << r))) {
                ++r;
                continue;
            }
            int end = r;
            while (end + 1 < 32 && (rows & (1u << (end + 1)))) ++end;
            const u32 bytes = static_cast<u32>(end - r + 1) * 64;
            u16* dst = commit::bgMap(screenBlock_ + b) + r * 32;
            const u16* src = shadow_ + b * 1024 + r * 32;
            if (commit::queueCopy(dst, src, bytes)) {
                for (int k = r; k <= end; ++k) rows &= ~(1u << k);
            } else {
                all = false;
            }
            r = end + 1;
        }
    }
    return all;
}

}  // namespace agb::render
